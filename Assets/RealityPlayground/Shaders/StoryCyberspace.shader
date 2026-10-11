Shader "RealityPlayground/StoryCyberspace"
{
    Properties
    {
        _Glow("Bounded signal glow",Range(0,2))=1.4
        _Flow("Signal current",Range(0,1))=1
        _Drift("Fragment drift",Range(0,1))=0
        _Voice("Voice energy",Range(0,1))=0
        _Accent("Syllable onset",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "FracturedCoordinates"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Glow,_Flow,_Drift,_Voice,_Accent;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 seed:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 local:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS.xyz;
                float t=_Time.y,seed=input.seed.x;
                p+=_Drift*float3(sin(t*.43+seed*17),cos(t*.51+seed*13),sin(t*.37+seed*19));

                float slip=step(.96,frac(t*.41+seed*.17))*_Accent;
                p.x+=slip*_Drift*.16;
                output.positionCS=TransformObjectToHClip(p);
                output.color=input.color;output.uv=input.uv;output.local=p;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color=input.color.rgb;
                if(_Flow>.001)
                {
                    float t=_Time.y;
                    float phase=input.uv.x*18+t*.74+input.local.y*.12;
                    half pulse=.5+.5*sin(phase*6.2831853);
                    half sweep=smoothstep(.78,.98,pulse);
                    half3 spectrum=.5+.5*cos(input.local.y*.19+input.local.z*.14-t*.22+float3(0,2.094,4.189));
                    color=lerp(color,spectrum,_Flow*(.28+_Voice*.22));
                    color*=.42+_Glow*(.29+sweep*.26+_Voice*.14+_Accent*.09);

                    float row=floor(input.local.y*4)+floor(input.uv.x*21);
                    float dropout=frac(sin(row*12.9898+floor(t*5))*43758.5453);
                    clip(dropout-(.018+_Accent*.018));
                }
                return half4(min(color,1.85),1);
            }
            ENDHLSL
        }
    }
}
