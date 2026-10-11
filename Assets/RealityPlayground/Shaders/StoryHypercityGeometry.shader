Shader "RealityPlayground/StoryHypercityGeometry"
{
    Properties
    {
        _Glow("Sculpted light",Range(0,2))=1.18
        _Naturalness("Matte architectural material",Range(0,1))=0
        _Roughness("Surface roughness",Range(0,1))=.87
        _Reveal("Digital layer removal",Range(0,1))=0
        _Seed("Installation offset",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+15" }
        Pass
        {
            Name "SculptedHypercity"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Glow;
                half _Naturalness;
                half _Roughness;
                half _Reveal;
                float _Seed;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                half4 color:COLOR;
                float2 surface:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half3 color:COLOR;
                half2 surface:TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS.xyz;

                float sway=sin(_Time.y*1.1+p.y*1.3+p.x*.4)*input.color.a*.021;
                p.xz+=float2(sway,sway*.43);
                output.positionWS=TransformObjectToWorld(p);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color.rgb;
                output.surface=input.surface;
                return output;
            }
            float GrainHash(float3 p)
            {
                p=frac(p*.1031);p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(_Reveal>.001)
                {
                    float3 cell=floor(input.positionWS*11);
                    float noise=frac(sin(dot(cell,float3(12.9898,78.233,39.425))+_Seed*4.17)*43758.5453);
                    clip(noise-_Reveal);
                }
                half3 normal=SafeNormalize(input.normalWS);
                half3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                if(_Naturalness>.5)
                {
                    Light key=GetMainLight();
                    half diffuse=saturate(dot(normal,key.direction));
                    half3 ambient=max(SampleSH(normal),half3(.13,.135,.14));

                    float3 grainPosition=input.positionWS*34;
                    half visibility=saturate(.5/max(length(fwidth(grainPosition)),.01));
                    half coarse=GrainHash(floor(input.positionWS*2.2));
                    half grain=(GrainHash(floor(grainPosition))-.5)*visibility;
                    half grainMod=1+((coarse-.5)*.07+grain*.07)*input.surface.y;
                    half3 albedo=input.color*grainMod;
                    half3 halfway=SafeNormalize(view+key.direction);
                    half specular=pow(saturate(dot(normal,halfway)),lerp(42,7,_Roughness))*(1-_Roughness)*.045;
                    half3 color=albedo*(ambient+key.color*diffuse*.72)+key.color*specular;
                    color+=albedo*input.surface.x;
                    return half4(min(color,half3(.85,.85,.85)),1);
                }
                half rim=1-saturate(abs(dot(view,normal)));rim*=rim;
                half shade=.58+.25*saturate(dot(normal,normalize(half3(-.3,.8,-.5))));
                float phase=input.positionWS.y*47-_Time.y*.5;
                float aa=max(fwidth(phase),.025);
                half scan=1-.065*(1-smoothstep(.13-aa,.13+aa,abs(frac(phase)-.5)));
                half pulse=.965+.035*sin(input.positionWS.y*1.7+_Time.y*1.15);
                half3 color=input.color*(shade+rim*.3)*_Glow*scan*pulse;
                color+=input.color*rim*.12;
                return half4(min(color,half3(1.65,1.65,1.65)),1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
