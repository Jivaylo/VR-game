Shader "RealityPlayground/BreachVolume"
{
    Properties { _Rupture("Rupture",Range(0,1))=0 _Intensity("Intensity",Range(0,6))=2.5 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Rupture; float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input); Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); return o;
            }
            float3 Spectrum(float t) { return .52+.48*cos(6.2831853*(t+float3(.0,.33,.67))); }
            float2 IntersectBox(float3 ro,float3 rd)
            {

                float3 directionSign=step(0,rd)*2-1;
                float3 inverse=directionSign/max(abs(rd),.00001);
                float3 a=(-.5-ro)*inverse,b=(.5-ro)*inverse;
                float3 near=min(a,b),far=max(a,b); return float2(max(max(near.x,near.y),near.z),min(min(far.x,far.y),far.z));
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(_Rupture<.001) discard;
                float3 origin=TransformWorldToObject(GetCameraPositionWS());
                float3 ray=SafeNormalize(TransformWorldToObject(input.positionWS)-origin);
                float2 interval=IntersectBox(origin,ray); float start=max(interval.x,0),finish=interval.y;
                if(finish<=start) discard;
                float stepLength=(finish-start)/40; float4 sum=0;
                float time=_Time.y; float3 p;
                [loop] for(int i=0;i<40;i++)
                {
                    float t=start+(i+.5)*stepLength; p=origin+ray*t;
                    float spin=p.z*2.8+time*.14;
                    float2 rotated=float2(cos(spin)*p.x-sin(spin)*p.y,sin(spin)*p.x+cos(spin)*p.y);
                    float angle=atan2(rotated.y,abs(rotated.x)+abs(rotated.y)<.00001 ? .00001 : rotated.x); float radius=length(rotated);
                    float petal=cos(angle*7+p.z*13-time*.55);
                    float tunnel=.24+.045*petal+.024*sin(p.z*32-time*1.1);
                    float web=exp(-abs(radius-tunnel)*85);
                    float3 f=float3(rotated*18,p.z*16-time*.28);
                    float gyroid=dot(sin(f),cos(f.yzx));
                    float lattice=exp(-abs(gyroid)*17)*smoothstep(.08,.32,radius)*.55;
                    float branches=exp(-abs(sin(angle*11+p.z*9+time*.4))*18)*exp(-abs(radius-.13-.05*sin(p.z*17))*40);
                    float curtain=pow(saturate(1-radius*2.25),2)*exp(-abs(sin(p.z*23+radius*9-time*.5))*23)*.65;
                    float boundary=saturate((.5-max(max(abs(p.x),abs(p.y)),abs(p.z)))*17);
                    float density=(web*1.5+lattice+branches*.7+curtain)*boundary;
                    float a=saturate(density*stepLength*7*_Rupture);
                    float3 colour=Spectrum(p.z*.9+petal*.13+radius*1.4+time*.035);
                    colour=lerp(colour,float3(.05,.75,1),lattice*.3);
                    sum.rgb+=(1-sum.a)*colour*a*_Intensity; sum.a+=(1-sum.a)*a;
                    if(sum.a>.98) break;
                }
                float2 screen=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 bend=float2(sin(screen.y*29+time*.65),cos(screen.x*31-time*.51))*.018*_Rupture;
                float3 scene=SampleSceneColor(saturate(screen+bend));
                float opacity=saturate(sum.a+.78*_Rupture);
                float3 backdrop=scene*.04+float3(.008,.003,.022);
                return half4(sum.rgb+backdrop*(opacity-sum.a),opacity);
            }
            ENDHLSL
        }
    }
}
