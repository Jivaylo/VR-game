Shader "RealityPlayground/StoryPortalFast"
{
    Properties
    {
        _Rupture("Rupture",Range(0,1))=1
        _Intensity("Intensity",Range(0,6))=2
        _PhaseOffset("Preview phase",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Name "AnalyticStereoTunnel"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Rupture, _Intensity, _PhaseOffset;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            float2 BoxInterval(float3 origin,float3 direction)
            {
                float3 inverse=(step(0,direction)*2-1)/max(abs(direction),.00001);
                float3 a=(-.5-origin)*inverse,b=(.5-origin)*inverse;
                float3 lo=min(a,b),hi=max(a,b);
                return float2(max(max(lo.x,lo.y),lo.z),min(min(hi.x,hi.y),hi.z));
            }
            float Thread(float phase,float thickness)
            {

                float footprint=max(fwidth(phase),.015);
                float wave=abs(sin(phase));
                float strand=1-smoothstep(thickness,thickness+footprint,wave);
                float halo=1-smoothstep(thickness,.72+footprint,wave);
                return (strand*.65+halo*.35)*(1-smoothstep(.8,2.8,footprint));
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Rupture-.001);
                float3 origin=TransformWorldToObject(GetCameraPositionWS());
                float3 direction=SafeNormalize(TransformWorldToObject(input.positionWS)-origin);
                float2 interval=BoxInterval(origin,direction);
                float entry=max(interval.x,0);
                clip(interval.y-entry);
                float3 front=origin+direction*entry;
                float2 q=front.xy*2;
                float radius=length(q);
                float edge=1-smoothstep(1.05,1.2,radius);
                clip(edge-.002);

                float2 lateral=direction.xy*2;
                float a=dot(lateral,lateral),b=dot(q,lateral);
                float c=dot(q,q)-1.44;
                float distance=a>.00001?(-b+sqrt(max(b*b-a*c,0)))/a:6/max(abs(direction.z),.08);
                float depth=clamp(distance*abs(direction.z),0,6);
                float travel=depth/max(abs(direction.z),.08);
                float2 surface=q+lateral*travel;
                float angle=atan2(surface.y,abs(surface.x)+abs(surface.y)<.00001?.00001:surface.x);
                float time=_Time.y+_PhaseOffset;

                depth+=sin(angle*7+depth*1.4-time*.45)*(.09+.07*saturate(depth*.2));
                float twist=angle+depth*.42+time*.13+sin(depth*1.1-time*.32)*.14;
                float wave=sin(twist*3-depth*2.3-time*.36);
                float lattice=Thread(twist*7+depth*8.5+wave*.85-time*.55,.075);
                float cross=Thread(twist*6-depth*6.8-wave*.6+time*.37,.055);
                float folds=Thread(depth*5.5+sin(twist*5)*.55-time*.72,.13);
                float arteries=Thread(twist*9+depth*1.7-time*.21,.055);
                float ribbons=.5+.5*sin(twist*5-depth*3.1+wave*.9+time*.45);
                ribbons=smoothstep(.25,.96,ribbons);
                float3 spectrum=.52+.48*cos(6.2831853*(depth*.14+twist*.16+wave*.04-time*.035+float3(0,.33,.67)));
                float3 secondary=.52+.48*cos(6.2831853*(depth*.11-twist*.12+time*.047+float3(.17,.5,.84)));
                float fade=lerp(1,.025,smoothstep(4.6,6,depth));
                float3 color=(spectrum*(.06+ribbons*.36+lattice*.38+folds*.46)+secondary*(cross*.28+arteries*.16))*fade;
                color+=float3(.008,.003,.022);
                float alpha=edge*saturate(_Rupture);
                return half4(color*min(_Intensity,3)*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
