Shader "RealityPlayground/RealityShard"
{
    Properties { _Amount ("Visible fracture", Range(0,1)) = 0 _Phase ("Elapsed", Float) = 0 _Seed ("Shard variation", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+90" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float _Amount, _Phase, _Seed;
            CBUFFER_END
            V vert(A input) { V o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
            half4 frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screen=GetNormalizedScreenSpaceUV(input.positionCS);
                float edge=min(min(input.uv.x,input.uv.y),1-input.uv.x-input.uv.y);
                float rim=1-smoothstep(.006,.04,edge);
                float2 offset=float2(sin(_Seed*19+_Phase*.4),cos(_Seed*31-_Phase*.6))*.1;
                float2 refractUV=saturate((screen-.5)*(1.15+sin(_Seed)*.25)+.5+offset);
                float3 glass;
                glass.r=SampleSceneColor(saturate(refractUV+float2(.012,0))).r;
                glass.g=SampleSceneColor(refractUV).g;
                glass.b=SampleSceneColor(saturate(refractUV-float2(.012,0))).b;
                float3 colour=.5+.5*cos(6.2831853*(_Seed+_Phase*.07+float3(0,.34,.67)));
                float lines=pow(saturate(sin((input.uv.x+input.uv.y)*85+_Seed*8)),18);
                return half4(glass*.55+colour*(rim*2.3+lines*.12+.11),saturate(_Amount*(.58+rim*.4)));
            }
            ENDHLSL
        }
    }
}
