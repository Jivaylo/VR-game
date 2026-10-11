Shader "RealityPlayground/StoryMembrane"
{
    Properties
    {
        _Peel("Reality tension",Range(0,1))=0
        _Membrane("Surrounding membrane",Range(0,1))=0
        [Toggle] _DepthWrite("Physical paper depth",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_DepthWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Peel, _Membrane;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input); Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.uv=input.uv; return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv-.5;
                float radius=length(p), angle=atan2(p.y,p.x), time=_Time.y;
                float pulse=sin(radius*52-angle*5-time*2.1);
                float3 spectrum=.5+.5*cos(6.283185*(radius*2-angle*.18+time*.05+float3(0,.33,.67)));
                float bands=pow(saturate(pulse),8);
                float scan=.86+.14*sin(input.uv.y*460-time*10);
                float edge=1-smoothstep(.43,.5,max(abs(p.x),abs(p.y)));
                float3 ink=float3(.009,.025,.043)+float3(.03,.21,.16)*(pow(saturate(1-radius*1.6),4)*.24);
                float grid=step(.972,frac(input.uv.x*19))+step(.978,frac(input.uv.y*27));
                ink+=grid*float3(.018,.065,.045);
                float2 screen=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset=float2(sin(angle*7+time),cos(radius*30-time))*.037*_Peel;
                float3 world=SampleSceneColor(saturate(screen+offset));
                float3 color=lerp(ink,spectrum*(1.3+bands*2.4)+world*.37,_Peel)*scan;
                float alpha=lerp(.98,edge*_Peel*.85,_Membrane);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
