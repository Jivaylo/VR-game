Shader "Garden/Break"
{
    Properties
    {
        _Color("Color",Color)=(.3,.7,.6,1)
        _Delay("Delay",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Delay;
            CBUFFER_END
            float _GardenBreak;
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                float t=saturate(_GardenBreak-_Delay);
                float strip=floor(output.world.y*9);
                output.world.x+=sin(strip*7.13)*t*t*.045;
                output.positionCS=TransformWorldToHClip(output.world);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t=saturate(_GardenBreak-_Delay);
                float3 cell=floor(input.world*7);
                float noise=frac(sin(dot(cell,float3(12.989,78.233,46.72)))*43758.5453);
                clip(noise-t);
                float edge=1-smoothstep(.015,.10,noise-t);
                float3 spectrum=.5+.5*cos(input.world.y*.6+float3(0,2.1,4.2)+t*4);
                half3 base=lerp(_Color.rgb,spectrum,t*.6);
                return half4(min(base*(.65+edge*.45),1.15),1);
            }
            ENDHLSL
        }
    }
}
