Shader "RealityPlayground/TransitionVeil"
{
    Properties
    {
        _Amount ("Fracture", Range(0,1)) = 0
        _Intensity ("Chromatic energy", Range(0,1.5)) = 1.1
        _Phase ("Elapsed time", Float) = 0
        _Progress ("Transition progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+80" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ShatterVisibleReality"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float _Amount, _Intensity, _Phase, _Progress;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 rotate2(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float interference(float2 p)
            {
                float f=0, a=.5;
                [unroll] for(int i=0;i<4;i++) { f+=a*noise(p); p=rotate2(p*2.07,.67)+3.1; a*=.48; }
                return f;
            }
            float3 spectrum(float value) { return .5 + .5*cos(6.2831853*(value+float3(0,.34,.67))); }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 p=uv-.5; float amount=_Amount; float time=_Phase; float t=_Progress;
                float aspect=_ScaledScreenParams.x/_ScaledScreenParams.y;
                float2 q=p*float2(aspect,1); float radius=length(q); float angle=atan2(q.y,q.x);
                float breakPhase=smoothstep(.16,.37,t)*(1-smoothstep(.7,.99,t));
                float gather=smoothstep(0,.17,t);
                float conceal=smoothstep(.4,.445,t)*(1-smoothstep(.58,.65,t));
                float2 grid=q*5.5; float2 baseCell=floor(grid), local=frac(grid); float first=9, second=9; float2 winning=0;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 cell=float2(x,y); float2 id=baseCell+cell;
                    float2 cellOffset=cell+float2(hash(id),hash(id+17.7))*.74+.13-local;
                    float d=dot(cellOffset,cellOffset);
                    if(d<first) { second=first; first=d; winning=id; } else second=min(second,d);
                }
                float fracture=1-smoothstep(.008,.039,second-first);
                float seed=hash(winning+9.8);
                float split=smoothstep(.14,.34,t)*(1-smoothstep(.72,1,t));
                float stagger=floor(time*7+seed*9)*.17;
                float2 shift=float2(sin(seed*44+stagger),cos(seed*27-stagger))*.14*split;
                float2 plate=rotate2(p-shift,(seed-.5)*.62*split);
                plate*=1+(seed-.45)*.5*split;
                float row=floor(uv.y*22);
                float slice=step(.7,hash(float2(row,floor(time*5))));
                plate.x+=sin(row*13.1+floor(time*5))*.19*slice*breakPhase;
                plate.y=lerp(plate.y,(floor(plate.y*42)+.5)/42,slice*breakPhase*.75);
                float sectors=7; float sector=6.2831853/sectors;
                float folded=abs(frac((angle+time*.14)/sector+.5)-.5)*sector;
                float warpedRadius=radius*(1+sin(radius*23-time*3)*.13*breakPhase);
                float2 kaleido=float2(cos(folded),sin(folded))*warpedRadius;
                kaleido=rotate2(kaleido,time*.13)*float2(1/aspect,1);
                float2 sampleUV=.5+lerp(plate,kaleido,breakPhase*.62);
                float aberration=(.002+breakPhase*.021)*amount;
                float2 chroma=normalize(p+float2(.001,.001))*aberration;
                float3 scene;
                scene.r=SampleSceneColor(saturate(sampleUV+chroma)).r;
                scene.g=SampleSceneColor(saturate(sampleUV)).g;
                scene.b=SampleSceneColor(saturate(sampleUV-chroma)).b;
                float3 negative=1-saturate(scene);
                scene=lerp(scene,negative*float3(.65,.27,.85),step(.83,seed)*breakPhase*.46);
                float turbulence=interference(q*5+float2(time*.23,-time*.31));
                float tendrils=pow(saturate(1-abs(sin(radius*36+angle*5-time*4+turbulence*9))),9);
                float iris=pow(saturate(1-abs(sin(radius*53-angle*4+time*3))),15);
                float3 colour=spectrum(turbulence*.8+seed*.14+time*.08);
                float wavefront=1-smoothstep(.008,.058,abs(radius-lerp(.02,1.1,saturate(t/.33))));
                float3 cracks=colour*(fracture*(.4+breakPhase*1.9)+wavefront*.9)*gather;
                float3 energy=spectrum(turbulence+radius-time*.075)*(tendrils*.6+iris*.3)*breakPhase;
                float3 voidColour=float3(.004,.003,.018)+spectrum(turbulence+radius*.3+time*.07)*(tendrils*.34+iris*.2);
                voidColour+=spectrum(angle*.12+time*.045)*pow(saturate(1-radius*1.6),5)*.14;
                float3 result=scene+cracks*_Intensity+energy*_Intensity;
                result=lerp(result,voidColour,conceal);
                float alpha=lerp(saturate(amount*.9+fracture*gather*.6),1,conceal);
                alpha*=smoothstep(0,.035,t)*(1-smoothstep(.96,1,t));
                return half4(result,alpha);
            }
            ENDHLSL
        }
    }
}
