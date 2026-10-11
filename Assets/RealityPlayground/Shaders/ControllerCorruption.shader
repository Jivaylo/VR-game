Shader "RealityPlayground/ControllerCorruption"
{
    Properties
    {
        _Growth("Corruption growth",Range(0,1))=0
        _ShellLayer("Outer membrane",Range(0,1))=0
        _Intensity("Intensity",Range(0,5))=2.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Growth; float _ShellLayer; float _Intensity;
                float4 _HandOrigin; float4 _HandRight; float4 _HandUp; float4 _HandForward;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float3 LocalHand(float3 p) { float3 q=p-_HandOrigin.xyz;return float3(dot(q,_HandRight.xyz),dot(q,_HandUp.xyz),dot(q,_HandForward.xyz)); }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Web(float2 p)
            {
                float2 cell=floor(p),f=frac(p);float a=10,b=10;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 o=float2(x,y);float2 r=o+float2(Hash(cell+o),Hash(cell+o+77.3))-f;float d=dot(r,r);
                    if(d<a){b=a;a=d;}else b=min(b,d);
                }
                return 1-smoothstep(.009,.12,b-a);
            }
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input); Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(input.positionOS.xyz);float3 normal=TransformObjectToWorldNormal(input.normalOS);
                float3 q=LocalHand(world);float swell=.5+.5*sin(q.z*77+q.y*54-_Time.y*2.6);
                world+=normal*(.0012+_ShellLayer*(.006+swell*.009)*_Growth);
                o.positionWS=world;o.normalWS=normal;o.positionCS=TransformWorldToHClip(world);return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 q=LocalHand(input.positionWS);float3 normal=normalize(input.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);float rim=pow(1-saturate(dot(normal,view)),2);
                float organic=sin(q.x*80+sin(q.y*73))*sin(q.z*63+q.y*40)*.014;
                float distanceFromTip=length(q-float3(0,0,.075));
                float front=_Growth*.36-distanceFromTip+organic;
                float growth=smoothstep(-.012,.02,front)*step(.003,_Growth);
                if(growth<.002) discard;
                float3 n=abs(normal);float3 weight=n/(n.x+n.y+n.z+.001);
                float web=Web(q.yz*43+sin(q.zy*25)*.12)*weight.x+Web(q.xz*43)*weight.y+Web(q.xy*43+4.3)*weight.z;
                float edge=exp(-abs(front)*85);
                float bands=.5+.5*sin(q.y*76-q.z*57+_Time.y*3.0);
                float iridescence=q.z*2.6+q.y*1.6+rim*.4+bands*.15+_Time.y*.06;
                float3 spectral=.52+.48*cos(6.2831853*(iridescence+float3(0,.33,.67)));
                float2 screen=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 distortion=normal.xy*(.004+web*.006)*growth;
                float3 glass=SampleSceneColor(saturate(screen+distortion));
                float3 colour=glass*.25+spectral*(.36+web*2+edge*1.4+rim*.7)*_Intensity;
                colour+=float3(.02,.8,1)*pow(web,4)*1.7;
                float alpha=growth*saturate(.37+web*.5+rim*.35+edge*.4);
                alpha*=lerp(1,.32+web*.32,_ShellLayer);
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
