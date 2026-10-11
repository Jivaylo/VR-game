Shader "RealityPlayground/StoryARProjection"
{
    Properties
    {
        _BaseColor ("Projection tint", Color) = (0.03, 0.9, 1, 0.9)
        _Glow ("Light intensity", Float) = 2
        _Reveal ("Reality failure", Range(0,1)) = 0
        _Seed ("Signal variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Glow, _Reveal, _Seed;
            CBUFFER_END
            float hash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS.xyz;
                float glitch=step(.82,hash(float3(floor(_Time.y*15),floor(p.y*9),_Seed*81)));
                p.x+=glitch*sin(_Time.y*61+_Seed*74)*_Reveal*.22;
                output.positionWS=TransformObjectToWorld(p);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                float rim=pow(saturate(1-abs(dot(SafeNormalize(input.normalWS),view))),2.5);
                float scanPhase=input.positionWS.y*155-_Time.y*3.2;
                float scan=.94+.06*sin(scanPhase)*(1-smoothstep(.65,3.14,fwidth(scanPhase)));
                float2 cell=floor(input.uv*float2(28,34));
                float noise=hash(float3(cell,_Seed*91));
                clip(noise-(max(0,_Reveal-.08)*.94));
                float edge=saturate(1-abs(noise-_Reveal)*15)*_Reveal;
                float pulse=1+sin(_Time.y*1.4+_Seed*7)*.045;
                float3 color=_BaseColor.rgb*(_Glow*(.65+rim*.35)*pulse+edge*4);
                color=lerp(color,.7+.7*cos(_Time.y*2+input.positionWS.y*.7+float3(0,2.1,4.2)),edge*.6);
                return half4(color,_BaseColor.a*scan*(1-_Reveal*.58));
            }
            ENDHLSL
        }
    }
}
