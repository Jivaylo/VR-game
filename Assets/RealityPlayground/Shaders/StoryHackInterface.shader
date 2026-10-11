Shader "RealityPlayground/StoryHackInterface"
{
    Properties
    {
        _Tint("Phosphor",Color)=(.05,1,.5,1)
        _Progress("Intrusion progress",Range(0,1))=0
        _Opacity("Visibility",Range(0,1))=1
        _Mode("Panel / scanner / packet path",Float)=0
        _Seed("Window identity",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Name "WorldSpaceIntrusion"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Progress, _Opacity, _Mode, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            float Narrow(float distance,float width)
            {
                float footprint=max(fwidth(distance),.001);
                return 1-smoothstep(width,width+footprint,distance);
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Opacity-.002);
                float2 uv=input.uv;
                float time=_Time.y;
                float light=0,alpha=0;
                if (_Mode>1.5)
                {
                    float packet=pow(saturate(sin(uv.x*39-time*17)),12);
                    light=.24+packet*.76;
                    alpha=.34+packet*.38;
                }
                else if (_Mode>.5)
                {
                    float2 q=(uv-.5)*2;
                    float radius=length(q);
                    float angle=atan2(q.y,abs(q.x)+abs(q.y)<.00001?.00001:q.x);
                    float ring=Narrow(abs(radius-(.63+.07*sin(time*2))),.0052);
                    float arc=Narrow(abs(radius-.88),.0065)*step(.25,sin(angle*7-time*2.2));
                    float inner=Narrow(abs(radius-.35),.0039)*step(.1,sin(angle*11+time*3));
                    float cross=Narrow(min(abs(q.x),abs(q.y)),.0039)*step(.5,radius)*step(radius,.99);
                    float scan=Narrow(abs(q.y-(frac(time*.55)*2-1)),.0065)*step(abs(q.x),.87);
                    float lock=Narrow(min(abs(abs(q.x)-.75),abs(abs(q.y)-.77)),.0085)*step(max(abs(q.x),abs(q.y)),.8)*step(.62,min(abs(q.x),abs(q.y)));
                    light=saturate(ring+arc+inner+cross+lock+scan*.6)*.65;
                    alpha=light*.7;
                }
                else
                {
                    float2 edge=min(uv,1-uv);
                    float border=Narrow(min(edge.x,edge.y),.007);
                    float corners=border*max(step(.88,uv.x)+step(uv.x,.12),step(.83,uv.y)+step(uv.y,.17));
                    float header=Narrow(abs(uv.y-.88),.0015);
                    float footer=Narrow(abs(uv.y-.11),.002);
                    float2 grid=abs(frac(uv*float2(26,15))-.5);
                    float gridLine=Narrow(min(grid.x,grid.y),.01)*.018;
                    float scan=Narrow(abs(uv.y-frac(time*.25+_Seed*.13)),.004)*.065;
                    float load=step(uv.x,.07+_Progress*.86)*step(.032,uv.y)*step(uv.y,.062)*step(.06,uv.x);
                    float ticks=step(.38,frac(uv.x*41))*load;
                    float2 packetCell=floor(uv*float2(70,90));
                    float packetBit=frac(sin(dot(packetCell,float2(7.31,11.73))+floor(time*8)+_Seed*3)*1743.37);
                    float packetFeed=step(.47,packetBit)*step(.069,uv.y)*step(uv.y,.092)*step(.12,frac(uv.x*70))*step(.93,1-uv.x);
                    light=.023+border*.22+corners*.54+header*.15+footer*.12+gridLine+scan+ticks*.58+packetFeed*.24;
                    alpha=.84+border*.1;

                }
                light=min(light,1.15);
                alpha=saturate(alpha)*_Opacity;
                half3 color=(_Tint.rgb*light*1.25+half3(.001,.008,.01))*_Opacity;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
