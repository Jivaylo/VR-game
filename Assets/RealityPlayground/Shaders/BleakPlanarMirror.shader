Shader "RealityPlayground/BleakPlanarMirror"
{
    Properties
    {
        _ReflectionLeft("Left eye reflection", 2D) = "black" {}
        _ReflectionRight("Right eye reflection", 2D) = "black" {}
        _Desaturation("Desaturation", Range(0,1)) = 0.985
        _Exposure("Exposure", Range(0.2,1.2)) = 0.59
        _HasReflection("Reflection ready", Float) = 0
        _MultipassEye("Multipass eye", Float) = 0
        _LiquidCoat("Controller liquid skin", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "BleakReflection"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ReflectionLeft); SAMPLER(sampler_ReflectionLeft);
            TEXTURE2D(_ReflectionRight); SAMPLER(sampler_ReflectionRight);
            CBUFFER_START(UnityPerMaterial)
                float4x4 _ReflectionVPLeft;
                float4x4 _ReflectionVPRight;
                float _Desaturation;
                float _Exposure;
                float _HasReflection;
                float _MultipassEye;
                float _LiquidCoat;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 normalWS : TEXCOORD2; float3 normalOS : TEXCOORD3; float deformation : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS += output.normalWS * (_LiquidCoat * .0015);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.normalOS = input.normalOS;
                output.deformation = abs(input.positionOS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float eyeIndex = _MultipassEye;
                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(UNITY_SINGLE_PASS_STEREO)
                    eyeIndex = unity_StereoEyeIndex;
                #endif
                float4 reflected = eyeIndex > .5 ? mul(_ReflectionVPRight, float4(input.positionWS,1)) : mul(_ReflectionVPLeft, float4(input.positionWS,1));
                float2 uv = reflected.xy / max(.0001, reflected.w) * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1 - uv.y;
                #endif

                float3 normalOS = normalize(input.normalOS);
                float tension = saturate(input.deformation * 7 + _LiquidCoat);
                uv += normalOS.xy * (.043 * tension + .004 * (1 - abs(normalOS.z)));
                float breath = sin(input.uv.y * 18 + _Time.y * .39) * .0009;
                uv.x += breath * (1 - tension);
                half3 color = SAMPLE_TEXTURE2D(_ReflectionLeft, sampler_ReflectionLeft, uv).rgb;
                if (eyeIndex > .5) color = SAMPLE_TEXTURE2D(_ReflectionRight, sampler_ReflectionRight, uv).rgb;
                half luminance = dot(color, half3(.2126,.7152,.0722));
                half bleak = pow(max(0, luminance), 1.18) * _Exposure;
                color = lerp(color * _Exposure, bleak.xxx, _Desaturation) * half3(.79,.88,1.04);
                float edge = saturate(pow(length((input.uv - .5) * 1.42), 3));
                color *= 1 - edge * .58;
                color += half3(.055,.001,.011) * edge * (.8 + .2 * sin(_Time.y * .63));

                float veinWave = sin(input.uv.y * 57 + sin(input.uv.x * 29) * 3 + sin(input.uv.y * 11) * 2);
                float veins = pow(saturate(veinWave), 25) * smoothstep(.65,.96, abs(input.uv.x - .5) * 2);
                color += half3(.015,.0002,.002) * veins * (1 - tension);
                float3 n = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1 - saturate(abs(dot(n,view))), 3);
                float3 lamp = normalize(float3(-.35,.8,-.5));
                float specular = pow(saturate(abs(dot(reflect(-view,n),lamp))), 42);
                half3 silver = half3(.64,.83,1.02) * (fresnel * 2.3 + specular * 2.7 + .11);
                color = lerp(color, color * 1.6 + silver, tension);
                color = lerp(half3(.11,.13,.15), color, _HasReflection);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
