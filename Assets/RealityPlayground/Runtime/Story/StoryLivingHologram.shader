Shader "RealityPlayground/StoryLivingHologram"
{
    Properties
    {
        _BaseColor ("Projection colour", Color) = (0.03,0.7,1,0.8)
        _Glow ("Bounded light intensity", Range(0,3)) = 2
        _Reveal ("Digital collapse", Range(0,1)) = 0
        _Seed ("Signal phase", Float) = 0
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
                float disruption=step(.8,hash(float3(floor(_Time.y*13),floor(p.y*10),_Seed*51)));
                p.x+=disruption*sin(_Time.y*39+_Seed*52)*_Reveal*.19;
                output.positionWS=TransformObjectToWorld(p);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view=normalize(GetCameraPositionWS()-input.positionWS+float3(.00001,0,.00001));
                float3 normal=normalize(input.normalWS+float3(0,.00001,0));

                normal*=dot(normal,view)<0 ? -1 : 1;
                float3 right=normalize(cross(float3(0,1,0),view)+float3(.00001,0,0));
                float3 key=normalize(view*.38+float3(0,.83,0)+right*.58);
                float keyLight=saturate(dot(normal,key)*.62+.38);
                float rim=pow(1-saturate(dot(normal,view)),2.25);
                float specular=pow(saturate(dot(normal,normalize(view+key))),32);
                float scanPhase=input.positionWS.y*105-_Time.y*4.2;
                float scan=.96+.04*sin(scanPhase)*(1-smoothstep(.65,3.14,fwidth(scanPhase)));
                float band=pow(saturate(.5+.5*sin(input.positionWS.y*6-_Time.y*2.3+_Seed*6.2)),14);
                float gridX=1-smoothstep(.025,.095,abs(frac(input.uv.x*18)-.5));
                float gridY=1-smoothstep(.025,.095,abs(frac(input.uv.y*18)-.5));
                float grid=max(gridX,gridY)*.055*(1-smoothstep(.15,.6,max(fwidth(input.uv.x*18),fwidth(input.uv.y*18))));
                float noise=hash(float3(floor(input.uv*float2(25,31)),_Seed*67));
                clip(noise-max(0,_Reveal-.05)*.98);

                float illumination=min(4.5,_Glow*(.55+keyLight*.68)+rim*2.05+specular*.75+band*.5+grid);
                float3 colour=_BaseColor.rgb*illumination;
                float alpha=_BaseColor.a*scan*(.87+rim*.13)*(1-_Reveal*.8);
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
