Shader "RealityPlayground/BreachBrick"
{
    Properties
    {
        _BaseColor("Brick colour", Color) = (0.44,0.22,0.15,1)
        _Rupture("Rupture", Range(0,1)) = 0
        _Crystal("Crystal fragment", Range(0,1)) = 0
        _Hack("Code takeover", Range(0,1)) = 0
        [HideInInspector] _BreachOrigin("Breach origin", Vector) = (0,0,0,0)
        [HideInInspector] _BreachRight("Breach right", Vector) = (1,0,0,0)
        [HideInInspector] _BreachUp("Breach up", Vector) = (0,1,0,0)
        [HideInInspector] _BreachForward("Breach forward", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Rupture;
                float _Crystal;
                float _Hack;
                float4 _BreachOrigin;
                float4 _BreachRight;
                float4 _BreachUp;
                float4 _BreachForward;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float2 WallPosition(float3 p) { float3 q=p-_BreachOrigin.xyz; return float2(dot(q,_BreachRight.xyz),dot(q,_BreachUp.xyz)); }
            float Crack(float2 p)
            {
                float2 cell=floor(p), f=frac(p); float d1=10,d2=10;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 o=float2(x,y); float n=Hash(cell+o);
                    float2 r=o+float2(n,Hash(cell+o+41.7))-f;
                    float d=dot(r,r); if(d<d1){d2=d1;d1=d;}else d2=min(d2,d);
                }
                return 1-smoothstep(0.012,0.08,d2-d1);
            }
            float3 Spectrum(float t) { return 0.52+0.48*cos(6.2831853*(t+float3(0,0.33,0.67))); }
            float Segment(float2 p,float2 centre,float2 halfSize,float aa)
            {
                float2 d=abs(p-centre)-halfSize;
                return 1-smoothstep(0,aa,max(d.x,d.y));
            }
            float Number(float2 p,int digit,float aa)
            {
                uint mask=digit==0?63:digit==1?6:digit==2?91:digit==3?79:digit==4?102:digit==5?109:digit==6?125:digit==7?7:digit==8?127:111;
                float value=0;
                value+=((mask&1)!=0)*Segment(p,float2(0,.34),float2(.17,.022),aa);
                value+=((mask&2)!=0)*Segment(p,float2(.20,.17),float2(.022,.14),aa);
                value+=((mask&4)!=0)*Segment(p,float2(.20,-.17),float2(.022,.14),aa);
                value+=((mask&8)!=0)*Segment(p,float2(0,-.34),float2(.17,.022),aa);
                value+=((mask&16)!=0)*Segment(p,float2(-.20,-.17),float2(.022,.14),aa);
                value+=((mask&32)!=0)*Segment(p,float2(-.20,.17),float2(.022,.14),aa);
                value+=((mask&64)!=0)*Segment(p,float2(0,0),float2(.17,.022),aa);
                return saturate(value);
            }
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input); Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(input.positionOS.xyz); float2 q=WallPosition(world);
                float region=saturate((_Rupture*2.25-length(q))/0.6)*_Rupture;
                float tear=sin(q.y*37+floor(_Time.y*7)*1.31)*sin(q.x*21+_Time.y*0.7);
                world+=_BreachForward.xyz*tear*region*0.035;
                o.positionWS=world; o.positionCS=TransformWorldToHClip(world); o.normalWS=TransformObjectToWorldNormal(input.normalOS); return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 q=WallPosition(input.positionWS); float3 normal=SafeNormalize(input.normalWS);
                float grain=Hash(floor((input.positionWS.xy+input.positionWS.z*.37)*210));
                Light main=GetMainLight(); float lambert=saturate(dot(normal,main.direction));
                float3 clay=_BaseColor.rgb*(0.84+grain*0.24)*(SampleSH(normal)+main.color*lambert*0.82+0.10);
                [branch] if(_Hack>.0001 && _Crystal<.5)
                {
                    float column=Hash(float2(floor(q.x*12),7));
                    float2 code=float2(q.x*12,q.y*14+_Time.y*(2+column*3));
                    float2 cell=floor(code),uv=frac(code)-.5;
                    int digit=(int)floor(Hash(cell+floor(_Time.y*5)*.17)*10);
                    float aa=max(.01,max(fwidth(code.x),fwidth(code.y))*.6);
                    float glyph=Number(uv,digit,aa);
                    float trail=frac(q.y*.43+column-_Time.y*.33);
                    float intensity=.24+trail*trail*1.25;
                    float front=smoothstep(0,.35,_Hack*4.8-length(q));
                    float3 numbers=float3(.003,.019,.009)+float3(.025,1,.19)*glyph*intensity;
                    clay=lerp(clay,numbers,front*saturate(_Hack*4));
                }

                [branch] if (_Rupture <= .0001 && _Crystal < .5) return half4(clay,1);
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float radius=length(q); float region=saturate((_Rupture*2.25-radius)/0.5)*_Rupture;
                float2 warped=q*6.3+float2(sin(q.y*7-_Time.y*.65),cos(q.x*9+_Time.y*.45))*region*.17;
                float crack=Crack(warped);
                float smallCrack=Crack(warped*2.7+4.4)*.45;
                float ridge=pow(1-abs(dot(view,normal)),3);
                float front=saturate(1-abs(radius-_Rupture*1.85)*8);
                float angle=atan2(q.y,abs(q.x)+abs(q.y)<.00001 ? .00001 : q.x);
                float flow=sin(radius*22-_Time.y*2.2+angle*3)*.5+.5;
                float3 spectral=Spectrum(radius*.45+_Time.y*.045+flow*.22+ridge*.35);
                float cracked=saturate(crack+smallCrack*.5);
                float membrane=smoothstep(.35,.9,region)*(0.22+flow*.18);
                float3 colour=lerp(clay,clay*.16+spectral*.32,membrane);
                colour+=spectral*(cracked*region*(2.8+flow*2)+front*cracked*.8*_Rupture);
                colour+=float3(.05,.8,1)*pow(cracked,4)*region*.65;
                if(_Crystal>.5) colour=Spectrum(dot(input.positionWS,float3(.47,.28,.33))+_Time.y*.1+ridge*.55)*(1.1+ridge*2.4+cracked);
                return half4(colour,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
