Shader "RealityPlayground/StoryEyelids"
{
    Properties { _Closure ("Eyelid closure", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+120" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Name "NaturalStereoBlink"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 eyeUV : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CBUFFER_START(UnityPerMaterial)
                float _Closure;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = float4(input.positionOS.xy * 2, UNITY_NEAR_CLIP_VALUE, 1);
                output.eyeUV = input.positionOS.xy + .5;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float closure = saturate(_Closure);
                float x = (input.eyeUV.x - .5) * 2;

                float upper = lerp(1.2, .47, closure) - .13 * closure * x * x;
                float lower = lerp(-.2, .47, closure) + .09 * closure * x * x;
                float feather = .012;
                float top = smoothstep(upper - feather, upper + feather, input.eyeUV.y);
                float bottom = 1 - smoothstep(lower - feather, lower + feather, input.eyeUV.y);
                float alpha = saturate(top + bottom);
                alpha = closure <= 0 ? 0 : (closure >= .999 ? 1 : alpha);
                return half4(0, 0, 0, alpha);
            }
            ENDHLSL
        }
    }
}
