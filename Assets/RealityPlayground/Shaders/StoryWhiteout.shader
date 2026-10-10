Shader "RealityPlayground/StoryWhiteout"
{
 Properties { _Opacity ("Opacity",Range(0,1))=0 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+90" "RenderType"="Transparent" }
 Pass { Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
 CBUFFER_START(UnityPerMaterial) float _Opacity; CBUFFER_END
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(.92,.97,1,_Opacity);}
 ENDHLSL } }
}
